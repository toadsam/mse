// file: last-round-backend/src/main/java/com/lastround/backend/entity/Augment.java
package com.lastround.backend.entity;

import jakarta.persistence.*;
import lombok.*;

// JPA entity for the augments table — represents an in-game power-up available to players.
@Getter
@Setter
@Builder
@NoArgsConstructor
@AllArgsConstructor
@Entity
@Table(name = "augments")
public class Augment {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, length = 80)
    private String name;

    @Column(nullable = false, length = 500)
    private String description;

    // Category tag used by the client to apply the augment effect (e.g., RELOAD_SPEED, MOVEMENT).
    @Column(nullable = false, length = 60)
    private String effectType;
}
