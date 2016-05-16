Rules:

* Any class that is final class that defines a table schema must reside in a file prefixed with "Mc", having the name of the class (e.g. McAccount).
* Any class not meeting the above description can't be prefixed with "Mc".
* In the case where multiple files are needed to describe a class as described above, use a folder that would take the name of the class (e.g. McContact). Within that folder have one file named the same.
* For classes that are to be used as abstract (in the DB sense) classes defining superclasses of final classes that define a table schemas, each must have its own file, and must have a class/file name prefixed with "McAbstr". "McAbstr" classes can be instantiated as object, they just can't have tables.
