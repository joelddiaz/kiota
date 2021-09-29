namespace Kiota.Builder.Writers.Shell
{
    public class ShellWriter : LanguageWriter
    {
        public ShellWriter(string rootPath, string clientNamespaceName)
        {
            PathSegmenter = new CSharpPathSegmenter(rootPath, clientNamespaceName);
            var conventionService = new CSharpConventionService();
            AddCodeElementWriter(new CodeClassDeclarationWriter(conventionService));
            AddCodeElementWriter(new CodeClassEndWriter(conventionService));
            AddCodeElementWriter(new CodeEnumWriter(conventionService));
            AddCodeElementWriter(new CodeIndexerWriter(conventionService));
            AddCodeElementWriter(new CodeMethodWriter(conventionService));
            AddCodeElementWriter(new CodePropertyWriter(conventionService));
            AddCodeElementWriter(new CodeTypeWriter(conventionService));

        }
    }
}
