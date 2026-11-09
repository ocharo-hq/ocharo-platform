// The be sdk's Serilog setup assigns the process-wide logger when a host builds, so parallel hosts would send one test's
// startup warnings to another's sinks (`Startup_ShouldWarnOnceNamingEveryRejectedAddress_WhenAddressesAreRejected`).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
