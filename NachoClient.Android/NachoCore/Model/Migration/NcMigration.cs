//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using SQLite;

using NachoCore.Utils;

namespace NachoCore.Model
{
    /// <summary>
    /// Base class for all migrations. Each migration is really a task that updates the database in some ways.
    /// </summary>
    public class NcMigration
    {
        // Convenient short hand for all migrations
        public SQLiteConnection Db {
            get {
                return NcModel.Instance.Db;
            }
        }

        // The total # of objects to be processed
        public static int TotalObjects { get; protected set; }

        // The # of objects processed (during this run)
        public static int ProcessedObjects { get; protected set; }

        // The highest migration version of the current build
        public static int CurrentVersion { get; protected set; }

        public NcMigration ()
        {
        }

        public int Version ()
        {
            string className = this.GetType ().Name;
            NcAssert.True (className.StartsWith ("NcMigration"));
            string versionString = className.Substring (11);
            return Convert.ToInt32 (versionString);
        }

        public static bool StartService ()
        {
            // Find all derived classes.
            var subclasses = (from assembly in AppDomain.CurrentDomain.GetAssemblies ()
                                       from type_ in assembly.GetTypes ()
                                       where type_.IsSubclassOf (typeof(NcMigration))
                                       select type_
                             );

            // Find the latest version
            var latestMigration = McMigration.QueryLatestMigration ();
            int lastMigration; // the latest complete migration
            if (null == latestMigration) {
                lastMigration = 0;
            } else {
                if (latestMigration.Finished) {
                    lastMigration = latestMigration.Version;
                } else {
                    lastMigration = latestMigration.Version - 1;
                }
            }

            // Filter out all migration versions that we have already finished.
            List<NcMigration> migrations = new List<NcMigration> ();
            foreach (var subclass in subclasses) {
                NcMigration migration = (NcMigration)Activator.CreateInstance (subclass, false);
                var version = migration.Version ();
                if (version > CurrentVersion) {
                    CurrentVersion = version;
                }
                if (version >= lastMigration) {
                    migrations.Add (migration);
                }
            }

            if (0 == migrations.Count) {
                return false; // no outstanding migration
            }

            // Run them all starting with the lowest version
            NcTask.Run (() => {
                foreach (var migration in migrations) {
                    var startTime = DateTime.Now;
                    var version = migration.Version ();
                    int rows;

                    // Set up the counters
                    ProcessedObjects = 0;
                    TotalObjects = 0;

                    var numObjects = migration.GetNumberOfObjects ();
                    Log.Info (Log.LOG_DB, "Migration {0} will process {1} objects", migration.Version (), numObjects);
                    TotalObjects = numObjects;

                    // Create or update the record of this migration
                    McMigration migrationRecord = McMigration.QueryByVersion (version);
                    if (null == migrationRecord) {
                        migrationRecord = new McMigration ();
                        migrationRecord.Version = version;
                        migrationRecord.StartTime = startTime;
                        rows = migrationRecord.Insert ();
                        NcAssert.True (1 == rows);
                    }
                    migrationRecord.NumberOfTimesRan += 1;

                    // Run the migration
                    Log.Info (Log.LOG_DB, "Running migration {0}...", version);
                    NcTask.Cts.Token.ThrowIfCancellationRequested ();
                    try {
                        migration.Run (NcTask.Cts.Token);
                        migrationRecord.DurationMsec += (int)(DateTime.Now - startTime).TotalMilliseconds;
                        migrationRecord.Finished = true;
                        rows = migrationRecord.Update ();
                        NcAssert.True (1 == rows);
                    } catch (OperationCanceledException) {
                        migrationRecord.DurationMsec += (int)(DateTime.Now - startTime).TotalMilliseconds;
                        rows = migrationRecord.Update ();
                        NcAssert.True (1 == rows);
                        throw;
                    }
                }
            }, "Migration");
            return true;
        }

        /// <summary>
        /// Return the number of db objects to be processed. The number is only for informative
        /// purpose. It does not have to be absolutely accurate.
        /// </summary>
        /// <returns>The number of objects.</returns>
        public virtual int GetNumberOfObjects ()
        {
            throw new NotImplementedException ();
        }

        /// <summary>
        /// The actual implementation of a migration. Between processing of consecutive objects,
        /// it must check for cancellation using the provided token. The only facility that are
        /// assumed available is DB. Telemetry events can be recorded but they will not uploaded
        /// until later.
        /// </summary>
        /// <param name="token">Token.</param>
        public virtual void Run (CancellationToken token)
        {
            throw new NotImplementedException ();
        }
    }
}

