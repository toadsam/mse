-- Unity 클라이언트의 augment 식별자(displayName)를 그대로 저장하기 위해 augment_name 컬럼을 추가한다.
-- Unity의 augment 세트와 DB augments 테이블의 시드 데이터가 서로 다르기 때문에,
-- augment_id(FK)로는 Unity가 선택한 augment를 표현할 수 없다.
-- 따라서 augment_name을 직접 저장하고, augment_id는 NULL을 허용하도록 완화한다(비파괴적 변경).

ALTER TABLE match_player_augments
    ADD COLUMN augment_name VARCHAR(80) NULL AFTER augment_id;

ALTER TABLE match_player_augments
    MODIFY COLUMN augment_id BIGINT NULL;
