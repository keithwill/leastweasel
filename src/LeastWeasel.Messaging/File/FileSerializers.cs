using LeastWeasel.Messaging.File;

namespace LeastWeasel.Messaging
{
    public static class FileSerializers
    {

        public static Serializer<FileChunkSend> GetFileChunkSend()
        {
            return new Serializer<FileChunkSend>()
            .Field(x => x.FilePath, (x, f) => x.FilePath = f)
            .Field(x => x.Offset, (x, f) => x.Offset = f)
            .Array(x => x.Data, (x, f) => x.Data = f)
            .Field(x => x.EndOfFile, (x, f) => x.EndOfFile = f)
            .Build();
        }

        public static Serializer<FileChunkSendResponse> GetFileChunkSendResponse()
        {
            return new Serializer<FileChunkSendResponse>()
            .Field(x => x.Success, (x, f) => x.Success = f)
            .Field(x => x.ErrorMessage, (x, f) => x.ErrorMessage = f)
            .Build();
        }

    }
}