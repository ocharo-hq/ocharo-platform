namespace BrandWorkspace.Api.Foundation.Readers;

/// <summary>Represents the messages a route answers when its multipart body cannot be read.</summary>
/// <param name="TooLarge">The message of a body whose declared length is over the limit.</param>
/// <param name="NotForm">The message of a body that is not multipart form data.</param>
/// <param name="Malformed">The message of a form that is malformed or over its limits.</param>
/// <param name="Unterminated">The message of a form that ended before its final boundary.</param>
public sealed record FormReadMessages(string TooLarge, string NotForm, string Malformed, string Unterminated);
