using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using NUnit.Framework;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
internal static class MachineGate
{
 internal static void Require()
 {
  if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("DOCAI_TEST_MACHINE") != "1")
   Assert.Ignore("Set DOCAI_TEST_MACHINE=1 on Windows to run certificate-store, DPAPI and TLS tests.");
 }
}
