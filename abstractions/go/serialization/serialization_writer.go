package serialization

import (
	i "io"
	"time"

	"github.com/google/uuid"
)

// SerializationWriter defines an interface for serialization of models to a byte array.
type SerializationWriter interface {
	i.Closer
	// WriteStringValue writes a String value to underlying the byte array.
	WriteStringValue(key string, value *string) error
	// WriteBoolValue writes a Bool value to underlying the byte array.
	WriteBoolValue(key string, value *bool) error
	// WriteInt32Value writes a Int32 value to underlying the byte array.
	WriteInt32Value(key string, value *int32) error
	// WriteInt64Value writes a Int64 value to underlying the byte array.
	WriteInt64Value(key string, value *int64) error
	// WriteFloat32Value writes a Float32 value to underlying the byte array.
	WriteFloat32Value(key string, value *float32) error
	// WriteFloat64Value writes a Float64 value to underlying the byte array.
	WriteFloat64Value(key string, value *float64) error
	// WriteByteArrayValue writes a ByteArray value to underlying the byte array.
	WriteByteArrayValue(key string, value []byte) error
	// WriteTimeValue writes a Time value to underlying the byte array.
	WriteTimeValue(key string, value *time.Time) error
	// WriteUUIDValue writes a UUID value to underlying the byte array.
	WriteUUIDValue(key string, value *uuid.UUID) error
	// WriteObjectValue writes a Parsable value to underlying the byte array.
	WriteObjectValue(key string, item Parsable) error
	// WriteCollectionOfObjectValues writes a collection of Parsable values to underlying the byte array.
	WriteCollectionOfObjectValues(key string, collection []Parsable) error
	// WriteCollectionOfStringValues writes a collection of String values to underlying the byte array.
	WriteCollectionOfStringValues(key string, collection []string) error
	// WriteCollectionOfBoolValues writes a collection of Bool values to underlying the byte array.
	WriteCollectionOfBoolValues(key string, collection []bool) error
	// WriteCollectionOfInt32Values writes a collection of Int32 values to underlying the byte array.
	WriteCollectionOfInt32Values(key string, collection []int32) error
	// WriteCollectionOfInt64Values writes a collection of Int64 values to underlying the byte array.
	WriteCollectionOfInt64Values(key string, collection []int64) error
	// WriteCollectionOfFloat32Values writes a collection of Float32 values to underlying the byte array.
	WriteCollectionOfFloat32Values(key string, collection []float32) error
	// WriteCollectionOfFloat64Values writes a collection of Float64 values to underlying the byte array.
	WriteCollectionOfFloat64Values(key string, collection []float64) error
	// WriteCollectionOfTimeValues writes a collection of Time values to underlying the byte array.
	WriteCollectionOfTimeValues(key string, collection []time.Time) error
	// WriteCollectionOfUUIDValues writes a collection of UUID values to underlying the byte array.
	WriteCollectionOfUUIDValues(key string, collection []uuid.UUID) error
	// GetSerializedContent returns the resulting byte array from the serialization writer.
	GetSerializedContent() ([]byte, error)
	// WriteAdditionalData writes additional data to underlying the byte array.
	WriteAdditionalData(value map[string]interface{}) error
}
