// file: last-round-backend/src/main/java/com/lastround/backend/entity/Match.java
package com.lastround.backend.entity;

import jakarta.persistence.*;
import lombok.*;

import java.time.LocalDateTime;

@Getter
@Setter
@Builder
@NoArgsConstructor
@AllArgsConstructor
@Entity
@Table(name = "matches", indexes = {
        @Index(name = "idx_matches_created_at", columnList = "createdAt"),
        @Index(name = "idx_matches_player1", columnList = "player1Id"),
        @Index(name = "idx_matches_player2", columnList = "player2Id"),
        @Index(name = "idx_matches_winner", columnList = "winnerId")
})
public class Match {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long player1Id;

    @Column(nullable = false)
    private Long player2Id;

    @Column(nullable = false)
    private Long winnerId;

    @Column(nullable = false)
    private Integer player1Score;

    @Column(nullable = false)
    private Integer player2Score;

    @Column(nullable = false, updatable = false)
    private LocalDateTime createdAt;

    @PrePersist
    protected void onCreate() {
        this.createdAt = LocalDateTime.now();
    }
}
