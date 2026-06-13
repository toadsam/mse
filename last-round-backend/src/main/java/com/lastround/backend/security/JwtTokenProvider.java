// file: last-round-backend/src/main/java/com/lastround/backend/security/JwtTokenProvider.java
package com.lastround.backend.security;

import io.jsonwebtoken.Claims;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.security.Keys;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import javax.crypto.SecretKey;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.Date;
import java.util.UUID;

// Handles JWT creation, parsing, and validation for both access and refresh tokens.
@Component
public class JwtTokenProvider {

    private final SecretKey key;
    private final long accessTokenMinutes;
    private final long refreshTokenDays;

    // Derives the HMAC signing key from the configured secret string.
    public JwtTokenProvider(
            @Value("${jwt.secret}") String secret,
            @Value("${jwt.access-token-expiration-minutes}") long accessTokenMinutes,
            @Value("${jwt.refresh-token-expiration-days}") long refreshTokenDays
    ) {
        this.key = Keys.hmacShaKeyFor(secret.getBytes(StandardCharsets.UTF_8));
        this.accessTokenMinutes = accessTokenMinutes;
        this.refreshTokenDays = refreshTokenDays;
    }

    // Creates a short-lived access token containing the userId (subject) and email claim.
    public String generateAccessToken(Long userId, String email) {
        Instant now = Instant.now();
        return Jwts.builder()
                .id(UUID.randomUUID().toString())
                .subject(String.valueOf(userId))
                .claim("email", email)
                .issuedAt(Date.from(now))
                .expiration(Date.from(now.plus(accessTokenMinutes, ChronoUnit.MINUTES)))
                .signWith(key)
                .compact();
    }

    // Creates a long-lived refresh token containing only the userId; used for token rotation.
    public String generateRefreshToken(Long userId) {
        Instant now = Instant.now();
        return Jwts.builder()
                .id(UUID.randomUUID().toString())
                .subject(String.valueOf(userId))
                .issuedAt(Date.from(now))
                .expiration(Date.from(now.plus(refreshTokenDays, ChronoUnit.DAYS)))
                .signWith(key)
                .compact();
    }

    // Parses and verifies the token signature; throws JwtException if invalid or expired.
    public Claims parseClaims(String token) {
        return Jwts.parser()
                .verifyWith(key)
                .build()
                .parseSignedClaims(token)
                .getPayload();
    }

    // Returns false for any token that fails signature verification or has expired.
    public boolean isValid(String token) {
        try {
            parseClaims(token);
            return true;
        } catch (Exception e) {
            return false;
        }
    }

    // Extracts the userId stored as the JWT subject claim.
    public Long getUserId(String token) {
        return Long.parseLong(parseClaims(token).getSubject());
    }

    public long getRefreshTokenDays() {
        return refreshTokenDays;
    }
}
