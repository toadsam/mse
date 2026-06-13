// file: last-round-backend/src/main/java/com/lastround/backend/dto/ApiResponse.java
package com.lastround.backend.dto;

import lombok.Builder;
import lombok.Getter;

// Generic envelope for all REST API responses: carries a success flag, payload, or error message.
@Getter
@Builder
public class ApiResponse<T> {
    private boolean success;
    private T data;
    private String error;

    // Convenience factory for successful responses.
    public static <T> ApiResponse<T> ok(T data) {
        return ApiResponse.<T>builder()
                .success(true)
                .data(data)
                .build();
    }

    // Convenience factory for error responses.
    public static <T> ApiResponse<T> fail(String error) {
        return ApiResponse.<T>builder()
                .success(false)
                .error(error)
                .build();
    }
}
