import { AuthenticationProvider, ParseNodeFactory, SerializationWriter } from "@microsoft/kiota-abstractions";
import { FetchOptions } from "./IFetchOptions";
import { Middleware } from "./middleware";

export interface RequestConfig {
    fetchOptions?: FetchOptions;
    customFetch?: Promise<Response>;
    authenticationProvider?: AuthenticationProvider;
    ParseNodeFactory: ParseNodeFactory;
    serializationWriterFactory: SerializationWriter;
    middleware?: Middleware | Middleware[];
}