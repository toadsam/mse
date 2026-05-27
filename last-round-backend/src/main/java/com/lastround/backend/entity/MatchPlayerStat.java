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
@Table(name = "match_player_stats", uniqueConstraints = {
        @UniqueConstraint(name = "uk_match_user_stats", columnNames = {"match_id", "user_id"})
}, indexes = {
        @Index(name = "idx_match_player_stats_match_id", columnList = "match_id"),
        @Index(name = "idx_match_player_stats_user_id", columnList = "user_id")
})
public class MatchPlayerStat {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "match_id", nullable = false)
    private Match match;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "user_id", nullable = false)
    private User user;

    @Column(nullable = false, length = 20)
    private String result;

    @Column(nullable = false)
    @Builder.Default
    private Integer score = 0;

    @Column(nullable = false, name = "damage_dealt")
    @Builder.Default
    private Integer damageDealt = 0;

    @Column(name = "character_name", length = 80)
    private String characterName;

    @Column(name = "created_at", nullable = false, updatable = false)
    private LocalDateTime createdAt;

    @PrePersist
    protected void onCreate() {
        if (this.createdAt == null) {
            this.createdAt = LocalDateTime.now();
        }
    }
}