/**
 * -------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.
 * See License in the project root for license information.
 * -------------------------------------------------------------------------------------------
 */

/**
 * @module RedirectHandler
 */

import { HttpMethod } from "@microsoft/kiota-abstractions";

import { FetchResponse } from "../utils/fetchDefinitions";
import { Middleware } from "./middleware";
import { MiddlewareContext } from "./middlewareContext";
import { RedirectHandlerOptionKey, RedirectHandlerOptions } from "./options/redirectHandlerOptions";

/**
 * @class
 * Class
 * @implements Middleware
 * Class representing RedirectHandler
 */
export class RedirectHandler implements Middleware {
	/**
	 * @private
	 * @static
	 * A member holding the array of redirect status codes
	 */
	private static REDIRECT_STATUS_CODES: Set<number> = new Set([
		301, // Moved Permanently
		302, // Found
		303, // See Other
		307, // Temporary Permanently
		308, // Moved Permanently
	]);

	/**
	 * @private
	 * @static
	 * A member holding SeeOther status code
	 */
	private static STATUS_CODE_SEE_OTHER = 303;

	/**
	 * @private
	 * @static
	 * A member holding the name of the location header
	 */
	private static LOCATION_HEADER = "Location";

	/**
	 * @private
	 * @static
	 * A member representing the authorization header name
	 */
	private static AUTHORIZATION_HEADER = "Authorization";

	/**
	 * @private
	 * @static
	 * A member holding the manual redirect value
	 */
	private static MANUAL_REDIRECT = "manual";

	/** Next middleware to be executed*/
	next: Middleware | undefined;
	/**
	 *
	 * @public
	 * @constructor
	 * To create an instance of RedirectHandler
	 * @param {RedirectHandlerOptions} [options = new RedirectHandlerOptions()] - The redirect handler options instance
	 * @returns An instance of RedirectHandler
	 */

	public constructor(private options: RedirectHandlerOptions = new RedirectHandlerOptions()) {
		this.options = options;
	}

	/**
	 * @private
	 * To check whether the response has the redirect status code or not
	 * @param {Response} response - The response object
	 * @returns A boolean representing whether the response contains the redirect status code or not
	 */
	private isRedirect(response: FetchResponse): boolean {
		return RedirectHandler.REDIRECT_STATUS_CODES.has(response.status);
	}

	/**
	 * @private
	 * To check whether the response has location header or not
	 * @param {Response} response - The response object
	 * @returns A boolean representing the whether the response has location header or not
	 */
	private hasLocationHeader(response: FetchResponse): boolean {
		return response.headers.has(RedirectHandler.LOCATION_HEADER);
	}

	/**
	 * @private
	 * To get the redirect url from location header in response object
	 * @param {Response} response - The response object
	 * @returns A redirect url from location header
	 */
	private getLocationHeader(response: FetchResponse): string {
		return response.headers.get(RedirectHandler.LOCATION_HEADER);
	}

	/**
	 * @private
	 * To check whether the given url is a relative url or not
	 * @param {string} url - The url string value
	 * @returns A boolean representing whether the given url is a relative url or not
	 */
	private isRelativeURL(url: string): boolean {
		return url.indexOf("://") === -1;
	}

	/**
	 * @private
	 * To check whether the authorization header in the request should be dropped for consequent redirected requests
	 * @param {string} requestUrl - The request url value
	 * @param {string} redirectUrl - The redirect url value
	 * @returns A boolean representing whether the authorization header in the request should be dropped for consequent redirected requests
	 */
	private shouldDropAuthorizationHeader(requestUrl: string, redirectUrl: string): boolean {
		const schemeHostRegex = /^[A-Za-z].+?:\/\/.+?(?=\/|$)/;
		const requestMatches: string[] = schemeHostRegex.exec(requestUrl);
		let requestAuthority: string;
		let redirectAuthority: string;
		if (requestMatches !== null) {
			requestAuthority = requestMatches[0];
		}
		const redirectMatches: string[] = schemeHostRegex.exec(redirectUrl);
		if (redirectMatches !== null) {
			redirectAuthority = redirectMatches[0];
		}
		return typeof requestAuthority !== "undefined" && typeof redirectAuthority !== "undefined" && requestAuthority !== redirectAuthority;
	}

	/**
	 * @private
	 * @async
	 * To update a request url with the redirect url
	 * @param {string} redirectUrl - The redirect url value
	 * @param {Context} context - The context object value
	 * @returns Nothing
	 */
	private updateRequestUrl(redirectUrl: string, context: MiddlewareContext) {
		context.requestUrl = redirectUrl;
	}

	/**
	 * @private
	 * @async
	 * To execute the next middleware and to handle in case of redirect response returned by the server
	 * @param {Context} context - The context object
	 * @param {number} redirectCount - The redirect count value
	 * @param {RedirectHandlerOptions} options - The redirect handler options instance
	 * @returns A promise that resolves to nothing
	 */
	private async executeWithRedirect(context: MiddlewareContext, redirectCount: number, options: RedirectHandlerOptions): Promise<FetchResponse> {
		const response = await this.next.execute(context);
		if (redirectCount < options.maxRedirects && this.isRedirect(response) && this.hasLocationHeader(response) && options.shouldRedirect(response)) {
			++redirectCount;
			if (response.status === RedirectHandler.STATUS_CODE_SEE_OTHER) {
				context.fetchRequestInit["method"] = HttpMethod.GET;
				delete context.fetchRequestInit.body;
			} else {
				const redirectUrl: string = this.getLocationHeader(response);
				if (context.fetchRequestInit.headers && !this.isRelativeURL(redirectUrl) && this.shouldDropAuthorizationHeader(context.requestUrl, redirectUrl)) {
					delete context.fetchRequestInit.headers[RedirectHandler.AUTHORIZATION_HEADER];
				}
				this.updateRequestUrl(redirectUrl, context);
			}
			return await this.executeWithRedirect(context, redirectCount, options);
		} else {
			return response;
		}
	}

	/**
	 * @public
	 * @async
	 * To execute the current middleware
	 * @param {Context} context - The context object of the request
	 * @returns A Promise that resolves to nothing
	 */
	public async execute(context: MiddlewareContext): Promise<FetchResponse> {
		const redirectCount = 0;
		const options: RedirectHandlerOptions = ((context?.requestInformationOptions && context.requestInformationOptions[RedirectHandlerOptionKey]) as RedirectHandlerOptions) || this.options;
		context.fetchRequestInit.redirect = RedirectHandler.MANUAL_REDIRECT;
		const response = await this.executeWithRedirect(context, redirectCount, options);
		return response;
	}
}
