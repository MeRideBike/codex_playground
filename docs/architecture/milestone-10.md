# Milestone 10: Executable verification and isolated review commits

Milestone 10 turns ForgeBoard's mutable demonstrations into end-to-end engineering runs. Each bundled mutable sample now includes a minimal, dependency-free .NET verification harness. After the reviewed correction, ForgeBoard runs the exact allowlisted harness in the application-owned run workspace, captures its exit code, duration, stdout, and stderr, and exposes that evidence in the test-engineer step.

ForgeBoard also creates a disposable Git baseline inside that workspace. Once a mission is completed, a person may explicitly record one isolated review commit. The resulting commit hash and diff summary are audit evidence, and the commit is restricted to the original sample manifest. No existing repository, remote, branch, or user worktree is touched.
