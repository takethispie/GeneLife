// The simulation engine and Arch worlds are exercised heavily by these tests.
// Running test collections in parallel exposes unsafe concurrent world access
// inside the ECS implementation, so the suite runs deterministically.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
