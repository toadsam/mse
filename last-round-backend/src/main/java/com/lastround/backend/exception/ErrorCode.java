// file: last-round-backend/src/main/java/com/lastround/backend/exception/ErrorCode.java
package com.lastround.backend.exception;

import lombok.Getter;
import org.springframework.http.HttpStatus;

// Enum mapping each business error to an HTTP status and a human-readable message.
// GlobalExceptionHandler reads status from here to set the response status code.
@Getter
public enum ErrorCode {
    INVALID_REQUEST(HttpStatus.BAD_REQUEST, "Invalid request"),
    USER_NOT_FOUND(HttpStatus.NOT_FOUND, "User not found"),
    EMAIL_ALREADY_EXISTS(HttpStatus.CONFLICT, "User ID already exists"),
    NICKNAME_ALREADY_EXISTS(HttpStatus.CONFLICT, "Nickname already exists"),
    INVALID_CREDENTIALS(HttpStatus.UNAUTHORIZED, "Invalid credentials"),
    WRONG_PASSWORD(HttpStatus.UNAUTHORIZED, "Wrong password"),
    INVALID_TOKEN(HttpStatus.UNAUTHORIZED, "Invalid token"),
    TOKEN_EXPIRED(HttpStatus.UNAUTHORIZED, "Token expired"),
    FORBIDDEN(HttpStatus.FORBIDDEN, "Forbidden");

    private final HttpStatus status;
    private final String message;

    ErrorCode(HttpStatus status, String message) {
        this.status = status;
        this.message = message;
    }
}
