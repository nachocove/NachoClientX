Commands pick up pending-q items that are appropriate, mark them as dispatched, and issue the command.

If there is a top level failure, then each dispatched item is rejected.

If there is an item level failure, then only that item is rejected.

What does "rejected" mean?

* If the error is retry-able, then there is no status-ind. the item is clicked, and marked not-dispatched.
* If the error is hard, then a status-ind goes to the app re: item.
  - question: do we attempt to revert the the item in the DB? Or does the app get the choice?
  
