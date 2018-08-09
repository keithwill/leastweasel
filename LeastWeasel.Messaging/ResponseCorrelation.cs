
using System.Threading.Tasks;

public struct ResponseCorrelation
{
    public ResponseCorrelation(long messageId, string method, TaskCompletionSource<object> taskCompletionSource)
    {
        this.TaskCompletionSource = taskCompletionSource;
        this.MessageId = messageId;
        this.Method = method;
    }

    public readonly TaskCompletionSource<object> TaskCompletionSource;
    public readonly long MessageId;
    public string Method;

}