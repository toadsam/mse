// file: last-round-backend/src/main/java/com/lastround/backend/entity/User.java
package com.lastround.backend.entity;

import jakarta.persistence.*;
import lombok.*;

import java.time.LocalDateTime;

// JPA entity for the users table. Email and nickname are enforced unique at the DB level.
@Getter
@Setter
@Builder
@NoArgsConstructor
@AllArgsConstructor
@Entity
@Table(name = "users", uniqueConstraints = {
        @UniqueConstraint(name = "uk_users_email", columnNames = "email"),
        @UniqueConstraint(name = "uk_users_nickname", columnNames = "nickname")
})
public class User {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, length = 100)
    private String email;

    // Stored as a BCrypt hash; never stored in plain text.
    @Column(nullable = false, length = 255)
    private String password;

    @Column(nullable = false, length = 30)
    private String nickname;

    // Set once on insert via @PrePersist; updatable=false prevents accidental mutation.
    @Column(nullable = false, updatable = false)
    private LocalDateTime createdAt;

    @PrePersist
    protected void onCreate() {
        this.createdAt = LocalDateTime.now();
    }
}
