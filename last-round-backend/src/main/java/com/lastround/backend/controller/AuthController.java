// file: last-round-backend/src/main/java/com/lastround/backend/controller/AuthController.java
package com.lastround.backend.controller;

import com.lastround.backend.dto.ApiResponse;
import com.lastround.backend.dto.auth.AuthResponse;
import com.lastround.backend.dto.auth.LoginRequest;
import com.lastround.backend.dto.auth.RefreshRequest;
import com.lastround.backend.dto.auth.SignupRequest;
import com.lastround.backend.exception.AppException;
import com.lastround.backend.exception.ErrorCode;
import com.lastround.backend.service.AuthService;
import jakarta.servlet.http.Cookie;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.ResponseCookie;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.time.Duration;

@RestController
@RequestMapping("/api/auth")
@RequiredArgsConstructor
public class AuthController {

    private final AuthService authService;

    @Value("${jwt.refresh-token-expiration-days}")
    private long refreshTokenDays;

    @Value("${jwt.refresh-cookie-name}")
    private String refreshCookieName;

    @PostMapping("/signup")
    public ApiResponse<AuthResponse> signup(@Valid @RequestBody SignupRequest request, HttpServletResponse response) {
        AuthResponse authResponse = authService.signup(request);
        addRefreshCookie(response, authResponse.getRefreshToken());
        return ApiResponse.ok(authResponse);
    }

    @PostMapping("/login")
    public ApiResponse<AuthResponse> login(@Valid @RequestBody LoginRequest request, HttpServletResponse response) {
        AuthResponse authResponse = authService.login(request);
        addRefreshCookie(response, authResponse.getRefreshToken());
        return ApiResponse.ok(authResponse);
    }

    @PostMapping("/refresh")
    public ApiResponse<AuthResponse> refresh(
            @RequestBody(required = false) RefreshRequest request,
            HttpServletRequest servletRequest,
            HttpServletResponse response
    ) {
        String refreshToken = extractRefreshToken(request, servletRequest);
        AuthResponse authResponse = authService.refresh(refreshToken);
        addRefreshCookie(response, authResponse.getRefreshToken());
        return ApiResponse.ok(authResponse);
    }

    private String extractRefreshToken(RefreshRequest request, HttpServletRequest servletRequest) {
        if (request != null && request.getRefreshToken() != null && !request.getRefreshToken().isBlank()) {
            return request.getRefreshToken();
        }

        if (servletRequest.getCookies() != null) {
            for (Cookie cookie : servletRequest.getCookies()) {
                if (refreshCookieName.equals(cookie.getName()) && cookie.getValue() != null && !cookie.getValue().isBlank()) {
                    return cookie.getValue();
                }
            }
        }

        throw new AppException(ErrorCode.INVALID_TOKEN, "Refresh token is required");
    }

    private void addRefreshCookie(HttpServletResponse response, String token) {
        ResponseCookie cookie = ResponseCookie.from(refreshCookieName, token)
                .httpOnly(true)
                .secure(false)
                .path("/")
                .maxAge(Duration.ofDays(refreshTokenDays))
                .sameSite("Lax")
                .build();
        response.addHeader("Set-Cookie", cookie.toString());
    }
}
