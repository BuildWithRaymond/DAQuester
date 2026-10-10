using DAQuester.DarkAges.Control;
using System.Text.Json;
namespace DAQuester.App;
internal static class ControlMain {
 public static async Task<int> RunAsync(string[] args) {
  if(args.Contains("--mcp")) {
   var protocol=new McpProtocol(r=>ControlPipe.CallAsync(r));
   while(await Console.In.ReadLineAsync() is {} line) {
    if(line.Length>65536){await Console.Error.WriteLineAsync("MCP input exceeds 64 KiB.");continue;}
    var response=await protocol.HandleAsync(line);if(response is not null){await Console.Out.WriteLineAsync(response);await Console.Out.FlushAsync();}
   }
   return 0;
  }
  try {
   int index=Array.IndexOf(args,"--control");
   var json=args.ElementAtOrDefault(index+1)??throw new InvalidOperationException("Usage: DAQuester.exe --control '{\"method\":\"state\"}'");
   if(File.Exists(json))json=await File.ReadAllTextAsync(json);
   var request=JsonSerializer.Deserialize<ControlRequest>(json,ControlPipe.Json)??throw new InvalidDataException("Invalid control request.");
   var result=await ControlPipe.CallAsync(request);await Console.Out.WriteLineAsync(JsonSerializer.Serialize(result,ControlPipe.Json));return 0;
  }catch(Exception e){await Console.Error.WriteLineAsync(e.Message);return 1;}
 }
}
