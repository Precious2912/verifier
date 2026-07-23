#!/bin/bash
# batch_inject.sh — inject N mixed faults tagged as one batch, verify, score, revert
set -e
N=$1
EVENTS_CONTAINER="event-pg"
INJECTOR="fault-injector/src/FaultInjector"
VERIFIER="verification-service/src/VerificationService"
FAULTS=(drop duplicate amount ghosttxn ghostbal)
SCENARIO="batch_$N"

echo "=== Batch $SCENARIO: injecting $N mixed faults ==="

docker exec -i "$EVENTS_CONTAINER" psql -U postgres -d events -q \
  -c "DELETE FROM event_store.verification_checkpoint;" 2>/dev/null || true

for ((i=0; i<N; i++)); do
  FAULT=${FAULTS[$((i % ${#FAULTS[@]}))]}
  FAULT_MODE=inject FAULT_TYPE=$FAULT SCENARIO=$SCENARIO dotnet run --project $INJECTOR
done

echo "=== Injected $N faults tagged '$SCENARIO'. Verifying... ==="
dotnet run --project $VERIFIER

echo "=== Reverting all $N faults ==="
FAULT_MODE=revert dotnet run --project $INJECTOR

ACTIVE=$(docker exec -i "$EVENTS_CONTAINER" psql -U postgres -d events -tAc \
  "SELECT COUNT(*) FROM evaluation.injected_faults WHERE reverted = FALSE;")
echo "Active faults after revert: $ACTIVE (should be 0)"