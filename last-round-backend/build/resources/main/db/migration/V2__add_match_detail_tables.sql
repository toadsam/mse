CREATE TABLE IF NOT EXISTS match_player_stats (
    id BIGINT NOT NULL AUTO_INCREMENT,
    match_id BIGINT NOT NULL,
    user_id BIGINT NOT NULL,
    result VARCHAR(20) NOT NULL,
    score INT NOT NULL DEFAULT 0,
    damage_dealt INT NOT NULL DEFAULT 0,
    character_name VARCHAR(80),
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT pk_match_player_stats PRIMARY KEY (id),
    CONSTRAINT fk_match_player_stats_match FOREIGN KEY (match_id) REFERENCES matches (id),
    CONSTRAINT fk_match_player_stats_user FOREIGN KEY (user_id) REFERENCES users (id),
    CONSTRAINT uk_match_user_stats UNIQUE (match_id, user_id),
    INDEX idx_match_player_stats_match_id (match_id),
    INDEX idx_match_player_stats_user_id (user_id)
);

CREATE TABLE IF NOT EXISTS match_player_augments (
    id BIGINT NOT NULL AUTO_INCREMENT,
    match_id BIGINT NOT NULL,
    user_id BIGINT NOT NULL,
    augment_id BIGINT NOT NULL,
    selected_order INT,
    selected_round INT,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT pk_match_player_augments PRIMARY KEY (id),
    CONSTRAINT fk_match_player_augments_match FOREIGN KEY (match_id) REFERENCES matches (id),
    CONSTRAINT fk_match_player_augments_user FOREIGN KEY (user_id) REFERENCES users (id),
    CONSTRAINT fk_match_player_augments_augment FOREIGN KEY (augment_id) REFERENCES augments (id),
    CONSTRAINT uk_match_user_round_order UNIQUE (match_id, user_id, selected_round, selected_order),
    INDEX idx_match_player_augments_match_id (match_id),
    INDEX idx_match_player_augments_user_id (user_id),
    INDEX idx_match_player_augments_augment_id (augment_id)
);