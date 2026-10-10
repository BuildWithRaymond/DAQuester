using Xunit;
using DAQuester.DarkAges.Control;
using System.Text.Json;
namespace DAQuester.Tests;
public class ControlPipeTests {
 [Fact] public async Task Pipe_transports_structured_requests_and_returns_handler_errors() {
  string name="DAQuester-test-"+Guid.NewGuid();ControlRequest? actual=null;
  await using var server=new ControlServer(r=>{actual=r;if(r.Method=="fail")throw new InvalidOperationException("Blocked test action");return Task.FromResult<object?>(new{connected=true});},name);server.Start();
  var result=(JsonElement)(await ControlPipe.CallAsync(new("state","Nuclear"),pipeName:name))!;
  Assert.True(result.GetProperty("connected").GetBoolean());Assert.Equal("Nuclear",actual!.Character);
  var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>ControlPipe.CallAsync(new("fail"),pipeName:name));Assert.Equal("Blocked test action",error.Message);
 }
}
