using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using ISTDocAi.Infrastructure.Services.DocAi.Intake;
using ISTDocAi.Infrastructure.Configuration.DocAi;


using Microsoft.Extensions.Options;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class FileInspectionTests
{
    
    [TestCase(true)]
    [TestCase(false)]
    public void Tiff_counts_IFDs_in_both_byte_orders(bool little)
    {
        var bytes=new byte[20];
        bytes[0]=bytes[1]=(byte)(little?'I':'M');
        if(little) { bytes[2]=42;bytes[4]=8;bytes[10]=14; }
        else {bytes[3]=42;bytes[7]=8;bytes[13]=14;}
        Check(bytes,"image/tiff",2);
    }
    
    [TestCase("jpeg")]
    [TestCase("png")]
    public void Image_magic_is_authoritative(string type)
    { Check(type=="jpeg" ? [255,216,255,224] : [137,80,78,71,13,10,26,10],type=="jpeg"?"image/jpeg":"image/png",1); }
    [Test]
    public void Cyclic_tiff_is_invalid()
    {
        byte[] bytes=[73,73,42,0,8,0,0,0,0,0,8,0,0,0];
        var path=Path.GetTempFileName();
        try { File.WriteAllBytes(path,bytes); var error=Assert.Throws<IntakeException>(()=>Inspector().Inspect(path,default)); Assert.Equal("INVALID_REQUEST",error.Code); }
        finally {File.Delete(path);}
    }
    private static FileInspector Inspector()=>new(Options.Create(new FileInspectionOptions()));
    private static void Check(byte[] bytes,string type,int pages)
    {
        var path=Path.GetTempFileName();
        try {File.WriteAllBytes(path,bytes);var result=Inspector().Inspect(path,default);Assert.Equal(type,result.ContentType);Assert.Equal(pages,result.PageCount);}
        finally {File.Delete(path);}
    }
}
