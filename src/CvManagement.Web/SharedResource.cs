namespace CvManagement.Web;

/// <summary>
/// Marker type only -- anchors Resources/SharedResource.{culture}.resx for IStringLocalizer&lt;SharedResource&gt;.
/// Deliberately lives at the project root, NOT inside Resources/: MSBuild's resx-to-manifest-name
/// generator uses the namespace of any same-named .cs file in the same folder (its "dependent file"
/// convention) in preference to folder-path inference. A marker file placed inside Resources/ would
/// hijack the resx's compiled name away from the "{RootNamespace}.{ResourcesPath}.{TypeName}" name
/// IStringLocalizer&lt;SharedResource&gt; actually searches for, and every lookup would silently
/// miss (falls back to returning the raw resource key).
/// </summary>
public class SharedResource;
