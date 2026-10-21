*Epic: Execute mutation testing on an F# project*

US-001 As a developer, I want to run Mutant against an F# project, so that I can evaluate the effectiveness of my test suite.
    a- wanna specify the source project to mutate, so that Mutant knows which codebase to analyze.
    b- wanna specify the test project to execute, so that Mutant can verify whether mutations are detected.
    c- wanna restrict mutation analysis to specific files, so that I can focus on a subset of the codebase.
    d- wanna restrict mutation analysis to specific directories, so that I can test large solutions incrementally.


US-002 As a developer, I want Mutant to discover possible mutations in my source code, so that I know which parts of the application can be challenged.

US-003 As a developer, I want Mutant to execute each mutation independently, so that I can understand the impact of every individual code change.

US-004 As a developer, I want Mutant to restore my original source code after execution, so that my working tree remains unchanged.

*Epic: Understand mutation results*

US-005 As a developer, I want to know whether a mutation was killed, survived, or caused a build failure, so that I can assess the quality of my tests.

US-006 As a developer, I want to see where a surviving mutation occurred, so that I can quickly investigate missing test coverage.

US-007 As a developer, I want to see the original code and the mutated code, so that I can understand exactly what behavior was changed.

US-008 As a developer, I want a summary of total mutations, killed mutations, survivors, and build failures, so that I can quickly evaluate the overall result.

*Epic: Improve tests*

US-009 As a developer, I want surviving mutations highlighted, so that I can prioritize writing additional tests.

US-010 As a developer, I want an explanation of what a surviving mutation implies, so that I can identify potential weaknesses in my test suite.

*Epic: Growth of mutation support*

US-011 As a developer, I want Mutant to support different mutation operators, so that it can challenge my tests in a variety of ways.

US-012 As a developer, I want new mutation operators to be added without changing existing discovery logic, so that the system remains easy to extend.




