// The be sdk's Serilog setup assigns the process-wide logger when a host builds, so parallel hosts would share one
// another's sinks; the hosts run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
