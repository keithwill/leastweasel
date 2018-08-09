
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MessagePack;

public class Service
{
    public string Name {get;set;}
    public Dictionary<string, Func<byte[], object>> RequestDeserializers;
    public Dictionary<string, Func<byte[], object>> ResponseDeserializers;
    public Dictionary<string, Func<object, Task<object>>> Handlers;
    private MD5CryptoServiceProvider md5;
    public Dictionary<long, string> HashMethodLookup;
    public Dictionary<string, long> MethodHashLookup;

    public Service()
    {
        RequestDeserializers = new Dictionary<string, Func<byte[], object>>();
        ResponseDeserializers = new Dictionary<string, Func<byte[], object>>();
        Handlers = new Dictionary<string, Func<object, Task<object>>>();
        this.HashMethodLookup = new Dictionary<long, string>();
        this.md5 = new MD5CryptoServiceProvider();
        this.MethodHashLookup = new Dictionary<string, long>();

    }

    public Service RegisterHandler<TRequest, TResponse>(string method, Func<TRequest, Task<TResponse>> handler)
    {
        RequestDeserializers.Add(method, (x) => LZ4MessagePackSerializer.Deserialize<TRequest>(x));
        // RequestDeserializers.Add(method, (bytes) => 
        // {
        //     Console.WriteLine($"Deserializing {bytes.Length} as {typeof(TResponse).Name} for {method}");
        //     var message = LZ4MessagePackSerializer.Deserialize<TRequest>(bytes);
        //     return message;
        // });
        Handlers.Add(method, async (message) => { return await handler((TRequest)message); });
        AddMethodHash(method);
        return this;
    }

    private void AddMethodHash(string method)
    {
        var methodHash = method.ComputeMD5HashAsInt64();
        HashMethodLookup.Add(methodHash, method);
        MethodHashLookup.Add(method, methodHash);
    }

    public Service Register<TRequest, TResponse>(string method)
    {
        RequestDeserializers.Add(method, (x) => LZ4MessagePackSerializer.Deserialize<TRequest>(x));
        ResponseDeserializers.Add(method, (x) => LZ4MessagePackSerializer.Deserialize<TResponse>(x));
        AddMethodHash(method);
        return this;
    }

    public Service Register<TRequest>(string method)
    {
        RequestDeserializers.Add(method, (x) => LZ4MessagePackSerializer.Deserialize<TRequest>(x));
        AddMethodHash(method);
        return this;
    }
    
}