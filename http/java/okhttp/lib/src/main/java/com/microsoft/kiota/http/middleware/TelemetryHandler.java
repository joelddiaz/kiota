package com.microsoft.kiota.http.middleware;

import com.microsoft.kiota.http.middleware.options.TelemetryOptions;
import okhttp3.Interceptor;
import okhttp3.Request;
import okhttp3.Response;

import javax.annotation.Nonnull;
import javax.annotation.Nullable;

public class TelemetryHandler implements Interceptor {



    private TelemetryOptions _telemetryOptions;

    public TelemetryHandler(@Nullable final TelemetryOptions telemetryOptions){
        if( telemetryOptions != null ) {
            this._telemetryOptions = telemetryOptions;
        }
        else {
            this._telemetryOptions = new TelemetryOptions();
        }
    }

    @Override
    @Nonnull
    public Response intercept(@Nonnull final Chain chain) {
        Request request =  chain.request();





        return chain.proceed(request);
    }

}
