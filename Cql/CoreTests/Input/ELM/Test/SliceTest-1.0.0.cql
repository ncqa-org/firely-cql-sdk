library SliceTest version '1.0.0'

using FHIR version '4.0.1'

define "Input list":
    {1, 2, 3, 4, 5, 6, 7, 8, 9, 10} as List<Integer>

define "n":
    5

//@operation: take-induced-slice
define fluent function "Take first n items"(list List<Integer>):
    Take(list, "n")

//@operation: take-induced-slice
define "Take first 5 items":
    Take("Input list", "n")


define fluent function "Take first n plus 1 items"(list List<Integer>):
    Take(list, "n")

define "Take first n plus 1 items":
    Take("Input list", "n")