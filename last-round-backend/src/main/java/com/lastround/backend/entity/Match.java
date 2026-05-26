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
    @Index(name = "idx_matches_created_at", columnList = "created_at"),
    @Index(name = "idx_matches_player1", columnList = "player1id"),
    @Index(name = "idx_matches_player2", columnList = "player2id"),
    @Index(name = "idx_matches_winner", columnList = "winner_id")
})
public class Match {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(name = "player1id", nullable = false)
    private Long player1Id;

    @Column(name = "player2id", nullable = false)
    private Long player2Id;

    @Column(name = "winner_id", nullable = false)
    private Long winnerId;

    @Column(name = "player1score", nullable = false)
    private Integer player1Score;

    @Column(name = "player2score", nullable = false)
    private Integer player2Score;

    @Column(name = "created_at", nullable = false, updatable = false)
    private LocalDateTime createdAt;

    @PrePersist
    protected void onCreate() {
        this.createdAt = LocalDateTime.now();
    }
}
