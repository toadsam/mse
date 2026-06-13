// file: last-round-backend/src/main/java/com/lastround/backend/exception/AppException.java
package com.lastround.backend.exception;

import lombok.Getter;

// Application-level runtime exception that carries a typed ErrorCode for HTTP status mapping.
@Getter
public class AppException extends RuntimeException {
    private final ErrorCode errorCode;

    // Uses the ErrorCode's default message.
    public AppException(ErrorCode errorCode) {
        super(errorCode.getMessage());
        this.errorCode = errorCode;
    }

    // Overrides the default message with a more specific one when needed.
    public AppException(ErrorCode errorCode, String message) {
        super(message);
        this.errorCode = errorCode;
    }
}
