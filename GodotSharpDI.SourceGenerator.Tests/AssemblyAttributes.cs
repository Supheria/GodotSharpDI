using Xunit;

// All test classes in this project share the global ErrorReporter static state,
// so parallel execution causes cross-test interference.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
