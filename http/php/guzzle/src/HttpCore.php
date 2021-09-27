<?php
namespace Microsoft\Kiota\Http\Guzzle;

use GuzzleHttp\Psr7\Request;
use Http\Adapter\Guzzle6\Client;
use Http\Promise\Promise;
use Http\Promise\RejectedPromise;
use Microsoft\Kiota\Abstractions\Authentication\AuthenticationProvider;
use Microsoft\Kiota\Abstractions\HttpCore as HttpCoreInterface;
use Microsoft\Kiota\Abstractions\RequestInformation;
use Microsoft\Kiota\Abstractions\ResponseHandler;
use Microsoft\Kiota\Abstractions\Serialization\ParseNodeFactory;
use Microsoft\Kiota\Abstractions\Serialization\ParseNodeFactoryRegistry;
use Microsoft\Kiota\Abstractions\Serialization\SerializationWriterFactory;
use Microsoft\Kiota\Abstractions\Serialization\SerializationWriterFactoryRegistry;
use Microsoft\Kiota\Abstractions\Store\BackingStoreFactory;

class HttpCore implements HttpCoreInterface
{

    private Client $client;
    private AuthenticationProvider $authenticationProvider;
    private ParseNodeFactory $parseNodeFactory;
    private SerializationWriterFactory $serializationWriterFactory;
    private bool $createdClient;
    public function __construct() {
        $this->client = new Client();
        $this->parseNodeFactory = $this->parseNodeFactory ?? ParseNodeFactoryRegistry::getDefaultInstance();
        $this->serializationWriterFactory = $this->serializationWriterFactory ?? SerializationWriterFactoryRegistry::getDefaultInstance();
    }

    /**
     * @throws \Exception
     */
    public function sendAsync(RequestInformation $requestInfo, $targetClass, ResponseHandler $responseHandler): Promise {
        return $this->client->sendAsyncRequest(new Request($requestInfo->httpMethod, $requestInfo->uri, $requestInfo->headers, $requestInfo->content));
    }

    public function getSerializationWriterFactory(): SerializationWriterFactory {
        return $this->serializationWriterFactory;
    }

    public function sendCollectionAsync(RequestInformation $requestInfo, $targetClass, ResponseHandler $responseHandler): Promise {
        return new RejectedPromise(new \InvalidArgumentException(''));
    }

    public function enableBackingStore(BackingStoreFactory $backingStoreFactory): void {

    }
}