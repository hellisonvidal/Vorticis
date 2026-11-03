-- Migração 094: cronograma da simulação Simples Nacional x IBS/CBS em core.parametro_legal.
DO $$
DECLARE v JSONB;
BEGIN
    SELECT valor INTO v FROM core.parametro_legal WHERE chave = 'REFORMA_SIMPLES' AND vigencia_inicio = DATE '2026-01-01';
    IF v IS NULL THEN RAISE EXCEPTION 'REFORMA_SIMPLES não semeado'; END IF;
    IF jsonb_array_length(v->'anos') <> 7 THEN RAISE EXCEPTION 'REFORMA_SIMPLES deve trazer 2027 a 2033'; END IF;
    IF (v->'anos'->6->>'icms_iss_extinto')::numeric <> 100 THEN RAISE EXCEPTION 'em 2033 ICMS e ISS devem estar extintos'; END IF;
    RAISE NOTICE 'OK 009_reforma_simples';
END $$;
