Commands pick up pending-q items that are appropriate, mark them as dispatched, and issue the command.

If there is a top level failure, then each dispatched item is rejected.

If there is an item level failure, then only that item is rejected.

What does "rejected" mean?

* If the error is retry-able, then there is no status-ind. the item is clicked, and marked not-dispatched.
  - Q: do we need to limit re-tries?
* If the error is hard, then a status-ind goes to the app re: item.
  - Q: do we attempt to revert the the item in the DB? Or does the app get the choice?
  

So: completed, hard-fail, hold-off.
