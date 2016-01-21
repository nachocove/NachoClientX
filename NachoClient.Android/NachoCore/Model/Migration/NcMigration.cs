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
    public delegate void NcMigrationDescriptionUpdateFunction (string descirption);

    public delegate void NcMigrationProgressUpdateFunction (float percentageComplete);

    public class NcMigrationComparer : IComparer<NcMigration>
    {
        public NcMigrationComparer ()
        {
        }

        public int Compare (NcMigration x, NcMigration y)
        {
            if (x.Version () < y.Version ()) {
                return -1;
            }
            if (x.Version () > y.Version ()) {
                return +1;
            }
            return 0;
        }
    }

    /// <summary>
    /// Base class for all migrations. Each migration is really a task that updates the database in some ways.
    /// </summary>
    public class NcMigration
    {
        int _Version = 0;

        bool Finished;

        static bool IsSetup;

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
        private static int _currentVersion = -1;

        private static Type[] _subclasses = null;
        private static Type[] Subclasses {
            get {
                if (null == _subclasses) {
                    _subclasses = (from type_ in Assembly.GetExecutingAssembly ().GetTypes ()
                        where type_.Name.StartsWith ("NcMigration") && type_.IsSubclassOf (typeof(NcMigration))
                        select type_
                    ).ToArray ();
                }
                return _subclasses;
            }
        }

        public static int CurrentVersion {
            get {
                if (-1 == _currentVersion) {
                    foreach (var subclass in Subclasses) {
                        string versionString = subclass.Name.Substring (11);
                        var version = Convert.ToInt32 (versionString);
                        if (version > _currentVersion) {
                            _currentVersion = version;
                        }
                    }
                }
                return _currentVersion;
            }
        }

        // The last migration version ran. (May not be complete)
        private static int _lastVersion = -1;

        public static int LastVersion {
            get {
                if (-1 == _lastVersion) {
                    throw new Exception ("Setup() must be called before this field can be accessed");
                }
                return _lastVersion;
            }
        }

        // All he migrations that need to be run
        private static List<NcMigration> _migrations { get; set; }

        private static List<NcMigration> migrations {
            get {
                if (null == _migrations) {
                    throw new Exception ("Setup() must be called before this field can be accessed");
                }
                return _migrations;
            }
        }

        public static int NumberOfMigrations {
            get {
                return migrations.Count;
            }
        }

        public static void Setup ()
        {
            if (IsSetup) {
                return;
            }
            IsSetup = true;

            _migrations = new List<NcMigration> ();
            // Find the latest version
            var latestMigration = McMigration.QueryLatestMigration ();
            int LastMigration; // the latest complete migration
            if (null == latestMigration) {
                LastMigration = 0;
                _lastVersion = 0;
            } else {
                _lastVersion = latestMigration.Version;
                if (latestMigration.Finished) {
                    LastMigration = latestMigration.Version;
                } else {
                    LastMigration = latestMigration.Version - 1;
                }
            }

            // Filter out all migration versions that we have already finished.
            foreach (var subclass in Subclasses) {
                var className = subclass.Name;
                string versionString = className.Substring (11);
                var version = Convert.ToInt32 (versionString);
                if (version > LastMigration) {
                    NcMigration migration = (NcMigration)Activator.CreateInstance (subclass, false);
                    _migrations.Add (migration);
                }
            }

            // Sort the migration
            _migrations.Sort (new NcMigrationComparer ());

            // If this is a fresh install, all migrations will be included because the table is empty
            // and it thinks no migration has been run.
            if (NcModel.Instance.FreshInstall) {
                var migrationRecord = new McMigration ();
                migrationRecord.Version = CurrentVersion;
                migrationRecord.StartTime = DateTime.UtcNow;
                migrationRecord.Finished = true;
                var rows = migrationRecord.Insert ();
                NcAssert.True (1 == rows);
                _migrations.Clear ();
                _lastVersion = CurrentVersion;
            }
        }

        private static NcMigrationProgressUpdateFunction ProgressUpdate;

        private static NcMigrationDescriptionUpdateFunction DescriptionUpdate;

        public NcMigration ()
        {
        }

        /// <summary>
        /// Called after updating ProcessedObjects to reflect the progress in UI.
        /// </summary>
        public void UpdateProgress (int numObjects)
        {
            ProcessedObjects += numObjects;

            if (null == ProgressUpdate) {
                return;
            }
            float percentageComplete = 0.0f;
            if (0 < TotalObjects) {
                percentageComplete = (float)ProcessedObjects / (float)TotalObjects;
                if (1.0 < percentageComplete) {
                    percentageComplete = 1.0f;
                }
            }
            ProgressUpdate (percentageComplete);
        }

        public static void UpdateDescription (string description)
        {
            if (null == DescriptionUpdate) {
                return;
            }
            DescriptionUpdate (description);
        }

        public int Version ()
        {
            if (0 == _Version) {
                string className = this.GetType ().Name;
                NcAssert.True (className.StartsWith ("NcMigration"));
                string versionString = className.Substring (11);
                _Version = Convert.ToInt32 (versionString);
            }
            return _Version;
        }

        public static bool WillStartService ()
        {
            return (0 < migrations.Where (x => !x.Finished).Count ()) || !IsCompatible ();
        }

        public static bool IsCompatible ()
        {
            return (CurrentVersion >= LastVersion);
        }

        public static void StartService (Action postRun, NcMigrationProgressUpdateFunction progressUpdate,
                                         NcMigrationDescriptionUpdateFunction descriptionUpdate)
        {
            if (0 == migrations.Count) {
                return; // no outstanding migration
            }

            ProgressUpdate = progressUpdate;
            DescriptionUpdate = descriptionUpdate;

            // Run them all starting with the lowest version
            NcTask.Run (() => {
                UpdateDescription ("");
                int n = 0;
                foreach (var migration in migrations) {
                    n += 1;
                    if (migration.Finished) {
                        continue;
                    }
                    UpdateDescription (String.Format ("Updating your app with latest features... ({0} of {1})", n, migrations.Count));

                    var startTime = DateTime.UtcNow;
                    var version = migration.Version ();
                    int rows;

                    // Set up the counters
                    ProcessedObjects = 0;
                    TotalObjects = 0;
                    migration.UpdateProgress (0);

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
                    migrationRecord.Update ();

                    // Run the migration
                    Log.Info (Log.LOG_DB, "Running migration {0}...", version);
                    NcTask.Cts.Token.ThrowIfCancellationRequested ();
                    try {
                        migration.Run (NcTask.Cts.Token);
                        migrationRecord.DurationMsec += (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                        migrationRecord.Finished = true;
                        rows = migrationRecord.Update ();
                        NcAssert.True (1 == rows);
                        migration.Finished = true;
                    } catch (OperationCanceledException) {
                        migrationRecord.DurationMsec += (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                        Log.Info (Log.LOG_DB, "Migration {0} interrupted", version);
                        rows = migrationRecord.Update ();
                        NcAssert.True (1 == rows);
                        throw;
                    }
                }

                if (null != postRun) {
                    postRun ();
                }
            }, "Migration");
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

