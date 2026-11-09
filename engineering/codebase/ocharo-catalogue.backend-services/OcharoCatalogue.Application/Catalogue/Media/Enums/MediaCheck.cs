namespace OcharoCatalogue.Application.Catalogue.Media.Enums;

/// <summary>Defines what a stored file's check found.</summary>
public enum MediaCheck
{
    /// <summary>The bytes are there and match their SHA-256.</summary>
    Ok,

    /// <summary>No such file, or its bytes are gone.</summary>
    Missing,

    /// <summary>The bytes are there but no longer match.</summary>
    Corrupt,

    /// <summary>The store could not be read just now.</summary>
    Unavailable,
}
