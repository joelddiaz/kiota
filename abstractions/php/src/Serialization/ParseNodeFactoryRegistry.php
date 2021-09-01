<?php
namespace Microsoft\Kiota\Abstractions\Serialization;


use Psr\Http\Message\StreamInterface;

class ParseNodeFactoryRegistry implements ParseNodeFactory {

    private static ?ParseNodeFactoryRegistry $defaultInstance = null;
    /**
     * @var array<string, ParseNodeFactory>
     */

    public array $contentTypeAssociatedFactories = [];

    public function getParseNode(string $contentType, StreamInterface $rawResponse): ParseNode {
        if (array_key_exists($contentType, $this->contentTypeAssociatedFactories)) {
            return $this->contentTypeAssociatedFactories[$contentType]->getParseNode($contentType, $rawResponse);
        }
        throw new \UnexpectedValueException('Content type ' . $contentType . ' does not have a factory to be parsed');
    }

    public static function getDefaultInstance(): ParseNodeFactoryRegistry {
        if (is_null(self::$defaultInstance)) {
            self::$defaultInstance = new self();
        }
        return self::$defaultInstance;
    }

    public function getValidContentType(): string {
        throw new \RuntimeException('The registry supports multiple content types. Get the registered factory instead.');
    }
}