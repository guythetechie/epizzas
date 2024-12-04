namespace api;

internal abstract record ApiErrorCode
{
    internal sealed record ResourceNotFound : ApiErrorCode
    {
        public static ResourceNotFound Instance { get; } = new();

        public override string ToString() => nameof(ResourceNotFound);
    }

    internal sealed record InvalidRequestParameter : ApiErrorCode
    {
        public static InvalidRequestParameter Instance { get; } = new();

        public override string ToString() => nameof(InvalidRequestParameter);
    }

    internal sealed record InvalidRequestBody : ApiErrorCode
    {
        public static InvalidRequestBody Instance { get; } = new();

        public override string ToString() => nameof(InvalidRequestBody);
    }

    internal sealed record InvalidRequestHeader : ApiErrorCode
    {
        public static InvalidRequestHeader Instance { get; } = new();

        public override string ToString() => nameof(InvalidRequestHeader);
    }

    internal sealed record ResourceAlreadyExists : ApiErrorCode
    {
        public static ResourceAlreadyExists Instance { get; } = new();

        public override string ToString() => nameof(ResourceAlreadyExists);
    }

    internal sealed record ETagMismatch : ApiErrorCode
    {
        public static ETagMismatch Instance { get; } = new();

        public override string ToString() => nameof(ETagMismatch);
    }
}