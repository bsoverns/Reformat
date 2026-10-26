namespace WpfReformat.Transforms
{
    /// <summary>Extra input an operation needs besides the main text box.</summary>
    public enum ExtraInput
    {
        None,
        Password,       // legacy AES: max 32 chars
        StrongPassword, // AES-GCM: any length
        JsonPath
    }

    /// <summary>Values gathered from the UI and handed to an operation. Image holds the bytes of a pasted/dropped image.</summary>
    public sealed record OperationContext(string Password = "", string Mask = "", byte[]? Image = null);

    /// <summary>One entry in the Options dropdown.</summary>
    public sealed record Operation(
        string Name,
        string Category,
        Func<string, OperationContext, string> Run,
        ExtraInput Extra = ExtraInput.None)
    {
        public override string ToString() => Name;
    }
}
