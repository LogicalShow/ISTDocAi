using System.Text;
namespace ISTDocAi.UI.Server.Tests.DocAi;

internal static class EncryptedPdf
{
    // A standard-security R2 dictionary with no valid empty-password entry.
    public static byte[] Create()
    {
        var text = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        string[] objects = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
            "<< /Filter /Standard /V 1 /R 2 /Length 40 /O <" + new string('0',64) + "> /U <" + new string('0',64) + "> /P -4 >>"];
        for(int i=0;i<objects.Length;i++) { offsets.Add(text.Length); text.Append($"{i+1} 0 obj\n{objects[i]}\nendobj\n"); }
        int xref=text.Length;
        text.Append("xref\n0 5\n0000000000 65535 f \n");
        foreach(int offset in offsets.Skip(1)) text.Append($"{offset:0000000000} 00000 n \n");
        text.Append($"trailer\n<< /Size 5 /Root 1 0 R /Encrypt 4 0 R /ID [<00112233445566778899aabbccddeeff><00112233445566778899aabbccddeeff>] >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(text.ToString());
    }
}
