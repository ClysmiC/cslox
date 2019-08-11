A C# implementation of Lox from http://craftinginterpreters.com/

This implementation is pretty poor when it comes to speed, error recovery, or pretty much any other metric that you would care about. I was mostly just concerned with correctness as this project was mostly just practice for me before I (potentially) go on to write a more serious compiler.

I did some implementation details differently, such as eschewing Exceptions and the Visitor pattern that the ebook heavily relies upon.

I also made some small semantic changes from the specification because I thought they made more sense. The error messages should guide you if you try to use an example program that doesn't conform to these differences. Probably the most apparent one is that I require the "fun" word preceding a class method declaration.

I did not implement inheritance, mostly because I pretty strongly dislike the way classes work in this language I didn't want to do much more work on them.

In fact, I don't like the language very much at all... but implementing it was certainly good practice.

There are probably bugs, I did not aggressively check the compiler's robustness.
