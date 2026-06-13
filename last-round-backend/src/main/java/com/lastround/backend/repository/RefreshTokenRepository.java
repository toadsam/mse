// file: last-round-backend/src/main/java/com/lastround/backend/repository/RefreshTokenRepository.java
package com.lastround.backend.repository;

import com.lastround.backend.entity.RefreshToken;
import com.lastround.backend.entity.User;
import org.springframework.data.jpa.repository.JpaRepository;

import java.time.LocalDateTime;
import java.util.Optional;

// JPA repository for the refresh_tokens table.
public interface RefreshTokenRepository extends JpaRepository<RefreshToken, Long> {
    // Looks up a token by its raw string value; used during token rotation and validation.
    Optional<RefreshToken> findByToken(String token);

    // Revokes all tokens for a user, e.g., on logout or account reset.
    void deleteByUser(User user);

    // Purges expired tokens; can be called periodically to keep the table clean.
    void deleteByExpiresAtBefore(LocalDateTime dateTime);
}
