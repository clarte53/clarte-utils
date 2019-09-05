Clarte Utils
===============

This repository contains shared utility code. It only defines independent and
reusable class / modules intended to be used in many projects.

Content
===============

The folowing namespaces are defined:
- 'Attributes' defines class decorators that can be added to objects to extend
  their behavior.
- 'Backport' (deprecated) adds support for modern .NET types and classes to
  older versions of the framework.
- 'Dev' contains tools to help debug and profile applications.
- 'Geometry' adds classes and extensions methods to extend the capabilities
  offered by Unity in regard to geometrical operations (matricial operations
  for example), as well as collision detection related things.
- 'Input' defines helper class to work with unity native API for VR tracking.
- 'Net' contains classes used for network communication. In particular, it
  defines an HTTP server and client, as well as a multi-channel negotiation
  protocol for easy networking independant of Unity network stack.
- 'Pattern' provide base implementation for various useful design patterns.
- 'Serialization' contains all serialization helper class. In particular, it
  defines a custom high performance binary serializer compatible with all
  platforms (including hololens).
- 'Threads' defines classes and helpers to simplify parallel development. In
  particular, it defines a platform agnostic wrapper for threads that can be
  used on hololens. It also defines thread pool, future and execution
  demultiplexion utilities.
- 'UI' contains interface helper class. In particular, it contains a VR gaze
  based interaction system for Unity GUI.

Guidelines for contributions
===============

The guidelines for the authorized code are the following:
- Code only. No assets, DLL, or other files formats.
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
