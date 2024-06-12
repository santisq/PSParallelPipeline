namespace PSParallelPipeline;

internal enum Type
{
    Success,
    Error
}

internal record struct PSOutputData(Type Type, object Output);
