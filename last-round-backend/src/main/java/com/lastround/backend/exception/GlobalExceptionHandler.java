// file: last-round-backend/src/main/java/com/lastround/backend/exception/GlobalExceptionHandler.java
package com.lastround.backend.exception;

import com.lastround.backend.dto.ApiResponse;
import org.springframework.http.ResponseEntity;
import org.springframework.validation.FieldError;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

import java.util.stream.Collectors;

// Centralized exception handler — converts exceptions into consistent ApiResponse error payloads.
@RestControllerAdvice
public class GlobalExceptionHandler {

    // Handles known business errors; HTTP status is derived from the ErrorCode.
    @ExceptionHandler(AppException.class)
    public ResponseEntity<ApiResponse<Object>> handleAppException(AppException ex) {
        return ResponseEntity.status(ex.getErrorCode().getStatus())
                .body(ApiResponse.fail(ex.getMessage()));
    }

    // Handles @Valid bean validation failures; joins all field error messages into one string.
    @ExceptionHandler(MethodArgumentNotValidException.class)
    public ResponseEntity<ApiResponse<Object>> handleValidationException(MethodArgumentNotValidException ex) {
        String message = ex.getBindingResult().getFieldErrors().stream()
                .map(FieldError::getDefaultMessage)
                .collect(Collectors.joining(", "));

        // Fall back to a generic message if no field-level messages are present.
        if (message.isBlank()) {
            message = ErrorCode.INVALID_REQUEST.getMessage();
        }

        return ResponseEntity.badRequest().body(ApiResponse.fail(message));
    }

    // Catch-all for unexpected errors; avoids leaking stack traces to the client.
    @ExceptionHandler(Exception.class)
    public ResponseEntity<ApiResponse<Object>> handleException(Exception ex) {
        return ResponseEntity.internalServerError().body(ApiResponse.fail(ex.getMessage()));
    }
}
