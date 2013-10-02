# High-Level Design Notes

## Data Model

The BE supports N Accounts. Each Account refers to a Cred, a Server and a ProtocolState object. Everything else in the DB carries an AccountId, and is applicable to that account only.
Every query of these records MUST have a qualifier like:

var folders = Db.Table<NcFolders> ().Where (rec => rec.AccountId == account.Id && ...

The UI updates the model immediately. The BE has to track the changes and "make it right" with the server. 
The PendingUpdates table is the sole repository of make-it-right state in the system. 
The flag IsDispatched is there to indicate that the update is being processed in the current query against the server.
On boot, any set IsDispatched flags must be reset as part of the boot sequence.

The UI sends an email by writing a new EmailMessage to the DB with IsAwatingSend set to true.
The BE will delete the EmailMessage from the DB after it has been sent.

The UI deletes an email by deleting the EmailMessage from the DB.
