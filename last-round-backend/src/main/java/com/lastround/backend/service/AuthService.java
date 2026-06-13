// file: last-round-backend/src/main/java/com/lastround/backend/service/AuthService.java
package com.lastround.backend.service;

import com.lastround.backend.dto.auth.AuthResponse;
import com.lastround.backend.dto.auth.LoginRequest;
import com.lastround.backend.dto.auth.SignupRequest;
import com.lastround.backend.entity.RefreshToken;
import com.lastround.backend.entity.User;
import com.lastround.backend.exception.AppException;
import com.lastround.backend.exception.ErrorCode;
import com.lastround.backend.repository.UserRepository;
import com.lastround.backend.security.JwtTokenProvider;
import lombok.RequiredArgsConstructor;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

// Service handling user registration, login, and JWT token lifecycle.
@Service
@RequiredArgsConstructor
public class AuthService {

    private final UserRepository userRepository;
    private final PasswordEncoder passwordEncoder;
    private final JwtTokenProvider jwtTokenProvider;
    private final RefreshTokenService refreshTokenService;

    // Creates a new user account after enforcing unique email and nickname, then issues tokens.
    @Transactional
    public AuthResponse signup(SignupRequest request) {
        if (userRepository.existsByEmail(request.getEmail())) {
            throw new AppException(ErrorCode.EMAIL_ALREADY_EXISTS);
        }
        if (userRepository.existsByNickname(request.getNickname())) {
            throw new AppException(ErrorCode.NICKNAME_ALREADY_EXISTS);
        }

        User user = User.builder()
                .email(request.getEmail())
                .password(passwordEncoder.encode(request.getPassword()))
                .nickname(request.getNickname())
                .build();

        User saved = userRepository.save(user);
        return issueTokens(saved);
    }

    // Authenticates the user by email/password and issues fresh tokens on success.
    @Transactional
    public AuthResponse login(LoginRequest request) {
        User user = userRepository.findByEmail(request.getEmail())
                .orElseThrow(() -> new AppException(ErrorCode.USER_NOT_FOUND));

        if (!passwordEncoder.matches(request.getPassword(), user.getPassword())) {
            throw new AppException(ErrorCode.WRONG_PASSWORD);
        }

        return issueTokens(user);
    }

    // Validates the existing refresh token, rotates it, and issues a new access token.
    @Transactional
    public AuthResponse refresh(String refreshToken) {
        RefreshToken tokenEntity = refreshTokenService.validate(refreshToken);
        User user = tokenEntity.getUser();

        String newAccessToken = jwtTokenProvider.generateAccessToken(user.getId(), user.getEmail());
        String newRefreshToken = jwtTokenProvider.generateRefreshToken(user.getId());

        // Token rotation: the old refresh token is replaced with the new one atomically.
        refreshTokenService.rotate(tokenEntity, newRefreshToken, jwtTokenProvider.getRefreshTokenDays());

        return AuthResponse.builder()
                .accessToken(newAccessToken)
                .refreshToken(newRefreshToken)
                .userId(user.getId())
                .email(user.getEmail())
                .nickname(user.getNickname())
                .build();
    }

    // Shared helper: generates both tokens, persists the refresh token, and builds the response.
    private AuthResponse issueTokens(User user) {
        String accessToken = jwtTokenProvider.generateAccessToken(user.getId(), user.getEmail());
        String refreshToken = jwtTokenProvider.generateRefreshToken(user.getId());

        refreshTokenService.create(user, refreshToken, jwtTokenProvider.getRefreshTokenDays());

        return AuthResponse.builder()
                .accessToken(accessToken)
                .refreshToken(refreshToken)
                .userId(user.getId())
                .email(user.getEmail())
                .nickname(user.getNickname())
                .build();
    }
}
