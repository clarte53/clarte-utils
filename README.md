Clarte Utils
===============

This repository contains shared utility code. It only defines independent and
reusable class / modules intended to be used in many projects.

The guidelines for the authorized code are the following:
- Classes, static or extension methods.
- Components with no license / copyright / confidentiality issues. Everything
  must be usable in every project.
- Independent components. They should not depend on anything else, except if
  those dependencies are also included.
- Only 'utility' functions, i.e. which are used often and are rarely modified.
- Every component must be named explicitly, with meaningful namespaces.
  In particular, everything must at least reside in the "CLARTE" namespace.
  Extension methods must also be defined in a valid namespace to avoid
  polluting types with unwanted extensions methods.
- Code should not use Debug.Log functions. Errors should be returned explicitly
  by every functions. Users should have the possibility to choose what is
  logged, including severity and message formating.
- Commented code. In particular, each public class or method must have
  comments following the C# XML conventions :
  https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/xml-documentation-comments
  https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/recommended-tags-for-documentation-comments
