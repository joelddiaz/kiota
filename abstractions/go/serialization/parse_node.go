package serialization

import (
	"time"

	"github.com/google/uuid"
)

// ParseNode Interface for a deserialization node in a parse tree. This interace provides an abstraction layer over serialization formats, libraries and implementations.
type ParseNode interface {
	// GetChildNode returns a new parse node for the given identifier.
	GetChildNode(index string) (ParseNode, error)
	// GetCollectionOfObjectValues returns the collection of Parsable values from the node.
	GetCollectionOfObjectValues(ctor func() Parsable) ([]Parsable, error)
	// GetCollectionOfPrimitiveValues returns the collection of primitive values from the node.
	GetCollectionOfPrimitiveValues(targetType string) ([]interface{}, error)
	// GetCollectionOfEnumValues returns the collection of Enum values from the node.
	GetCollectionOfEnumValues(parser func(string) (interface{}, error)) ([]interface{}, error)
	// GetObjectValue returns the Parsable value from the node.
	GetObjectValue(ctor func() Parsable) (Parsable, error)
	// GetStringValue returns a String value from the nodes.
	GetStringValue() (*string, error)
	// GetBoolValue returns a Bool value from the nodes.
	GetBoolValue() (*bool, error)
	// GetFloat32Value returns a Float32 value from the nodes.
	GetFloat32Value() (*float32, error)
	// GetFloat64Value returns a Float64 value from the nodes.
	GetFloat64Value() (*float64, error)
	// GetInt32Value returns a Int32 value from the nodes.
	GetInt32Value() (*int32, error)
	// GetInt64Value returns a Int64 value from the nodes.
	GetInt64Value() (*int64, error)
	// GetTimeValue returns a Time value from the nodes.
	GetTimeValue() (*time.Time, error)
	// GetUUIDValue returns a UUID value from the nodes.
	GetUUIDValue() (*uuid.UUID, error)
	// GetEnumValue returns a Enum value from the nodes.
	GetEnumValue(parser func(string) (interface{}, error)) (interface{}, error)
	// GetByteArrayValue returns a ByteArray value from the nodes.
	GetByteArrayValue() ([]byte, error)
}
