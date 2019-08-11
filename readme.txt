A C# implementation of Lox from http://craftinginterpreters.com/

I did some implementation details differently, such as eschewing Exceptions and the Visitor pattern that the ebook heavily relies upon.

I also made some small semantic changes from the specification because I thought they made more sense. The error messages should guide you if you try to use an example program that doesn't conform to these differences.

I did not implement inheritance, mostly because I pretty strongly dislike the way classes work in this language I didn't want to do much more work on them.

In fact, I don't like the language very much at all... but implementing it was certainly good practice.

There are probably bugs, I did not aggressively check the compiler's robustness.
