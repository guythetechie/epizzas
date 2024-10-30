namespace api;

public abstract record ApiErrorCode
{
    public sealed record ResourceNotFound : ApiErrorCode
    {
        public static ResourceNotFound Instance { get; } = new();

        public override string ToString() => nameof(ResourceNotFound);
    }

    public sealed record InvalidRequestParameter : ApiErrorCode
    {
        public static InvalidRequestParameter Instance { get; } = new();

        public override string ToString() => nameof(InvalidRequestParameter);
    }

    public sealed record InvalidRequestBody : ApiErrorCode
    {
        public static InvalidRequestBody Instance { get; } = new();

        public override string ToString() => nameof(InvalidRequestBody);
    }

    public sealed record ResourceAlreadyExists : ApiErrorCode
    {
        public static ResourceAlreadyExists Instance { get; } = new();

        public override string ToString() => nameof(ResourceAlreadyExists);
    }
}