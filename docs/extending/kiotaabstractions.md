---
parent: Understand and Extend the Kiota generator
---

# Kiota abstractions

On most platforms there are a range of different HTTP client library implementations. Developers often have preferences on which is the best implementation to meet their needs. Kiota's objective is to make it easier to create a HTTP request object but attempts to be agnostic of the library that will make the HTTP call. In order to decouple Kiota from specific HTTP libraries, we have defined a set of abstractions.

## Requests

This section provides information about the types offered in the abstractions library the generated result depends on which are related to executing requests.

### Request adapter

The Request Adapter interface is the primary point where Kiota service libraries will trigger the creation of a HTTP request.  Below is the [C# implementation](https://github.com/microsoft/kiota/blob/main/abstractions/dotnet/src/IRequestAdapter.cs).

```csharp
public interface IRequestAdapter {
    void EnableBackingStore(IBackingStoreFactory backingStoreFactory);

    ISerializationWriterFactory SerializationWriterFactory { get; }

    Task<ModelType> SendAsync<ModelType>(RequestInformation requestInfo,
                                            IResponseHandler responseHandler = default)
                    where ModelType : IParsable;

    Task<IEnumerable<ModelType>> SendCollectionAsync<ModelType>(RequestInformation requestInfo,
                    IResponseHandler responseHandler = default)
                where ModelType : IParsable;

    Task<ModelType> SendPrimitiveAsync<ModelType>(RequestInformation requestInfo,
                                                    IResponseHandler responseHandler = default);

    Task SendNoContentAsync(RequestInformation requestInfo,
                            IResponseHandler responseHandler = default);
}
```

Kiota service libraries return the model type that is associated with HTTP resource. This behavior can be overridden by changing the `responseHandler` to do something different than default behavior.  One use of this is to change the response type to be either a native HTTP response class, or return a generic API response class that provides access to more underlying metadata.

### Request information

In order to enable Kiota service libraries to make requests, they need to be able accumulate information about the request and pass it to the core library. The RequestInformation object is designed to do that. It only contains properties that be provided by the request builders. As request builders get more sophisticated, so may the RequestInformation class.

```csharp
public class RequestInformation
{
    public string UrlTemplate { get; set; }
    public Uri URI { get; set; }
    public HttpMethod HttpMethod { get; set; }
    public IDictionary<string, object> QueryParameters { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IDictionary<string, object> PathParameters { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    public Stream Content { get; set; }
    public IEnumerable<IRequestOption> RequestOptions { get }
}
```

### Response handler

When passed to the execution method from the fluent style API, this allows core to do all the default hard work, but enables a custom response handler to change the behavior of and access the native response object.

```csharp
public interface IResponseHandler
{
    Task<ModelType> HandleResponseAsync<NativeResponseType, ModelType>(NativeResponseType response);
}
```

## Serialization

This section provides information about the types offered in the abstractions library the generated result depends on which are related to serializing and deserializing payloads.

Please refer to the [serialization](./serialization.md) documentation page for detailed information.

## Backing store

This section provides information about the types offered in the abstractions library the generated result depends on which are related to storing models data in a third party data source.

### Backed model

This interface defines the members a model needs to implement when using a backing store to store the information instead of model fields. It provides accessors to the backing store.

### Backing store

This interface defines the members a backing store needs to implement for a model to be able to store it's field values in a third party data store instead of using fields.

```csharp
public interface IBackingStore
{
    T Get<T>(string key);
    void Set<T>(string key, T value);
    IEnumerable<KeyValuePair<string, object>> Enumerate();
    IEnumerable<string> EnumerateKeysForValuesChangedToNull();
    string Subscribe(Action<string, object, object> callback);
    void Subscribe(Action<string, object, object> callback, string subscriptionId);
    void Unsubscribe(string subscriptionId);
    void Clear();
    bool InitializationCompleted { get; set; }
    bool ReturnOnlyChangedValues { get; set; }
}
```

### In memory backing store

Default implementation that stores information in a map/dictionary in memory that enables for dirty tracking of changes on a model and reuse of models.

## Authentication

This section provides information about the types offered in the abstractions library the generated result depends on which are related to authenticating requests before submitting them.

Please refer to the [authentication](./authentication.md) documentation page for detailed information.
