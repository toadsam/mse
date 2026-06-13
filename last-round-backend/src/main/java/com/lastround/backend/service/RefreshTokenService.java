// file: last-round-backend/src/main/java/com/lastround/backend/service/RefreshTokenService.java
package com.lastround.backend.service;

import com.lastround.backend.entity.RefreshToken;
import com.lastround.backend.entity.User;
import com.lastround.backend.exception.AppException;
import com.lastround.backend.exception.ErrorCode;
import com.lastround.backend.repository.RefreshTokenRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.LocalDateTime;

// Service managing the lifecycle of refresh tokens: creation, validation, rotation, and cleanup.
@Service
@RequiredArgsConstructor
public class RefreshTokenService {

    private final RefreshTokenRepository refreshTokenRepository;

    // Revokes any existing token for the user before saving the new one (one active token per user).
    @Transactional
    public RefreshToken create(User user, String token, long expirationDays) {
        refreshTokenRepository.deleteByUser(user);
        RefreshToken refreshToken = RefreshToken.builder()
                .user(user)
                .token(token)
                .expiresAt(LocalDateTime.now().plusDays(expirationDays))
                .build();
        return refreshTokenRepository.save(refreshToken);
    }

    // Looks up the token by value and rejects it if it has passed the expiration timestamp.
    @Transactional(readOnly = true)
    public RefreshToken validate(String token) {
        RefreshToken refreshToken = refreshTokenRepository.findByToken(token)
                .orElseThrow(() -> new AppException(ErrorCode.INVALID_TOKEN));

        if (refreshToken.getExpiresAt().isBefore(LocalDateTime.now())) {
            throw new AppException(ErrorCode.TOKEN_EXPIRED);
        }

        return refreshToken;
    }

    // Mutates the existing entity in-place; the surrounding transaction flushes the update.
    @Transactional
    public void rotate(RefreshToken refreshToken, String newToken, long expirationDays) {
        refreshToken.setToken(newToken);
        refreshToken.setExpiresAt(LocalDateTime.now().plusDays(expirationDays));
    }

    // Deletes all tokens past their expiration date; call periodically to prevent table bloat.
    @Transactional
    public void cleanupExpiredTokens() {
        refreshTokenRepository.deleteByExpiresAtBefore(LocalDateTime.now());
    }
}
