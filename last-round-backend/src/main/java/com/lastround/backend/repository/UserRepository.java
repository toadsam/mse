// file: last-round-backend/src/main/java/com/lastround/backend/repository/UserRepository.java
package com.lastround.backend.repository;

import com.lastround.backend.entity.User;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.Optional;

public interface UserRepository extends JpaRepository<User, Long> {
    Optional<User> findByEmail(String email);

    boolean existsByEmail(String email);

    boolean existsByNickname(String nickname);
}
