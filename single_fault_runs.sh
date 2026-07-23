#!/bin/bash
set -e

FAULT=$1
N=${2:-5}

EVENTS_CONTAINER="event-pg"

INJECTOR="fault-injector/src/FaultInjector"
VERIFIER="verification-service/src/VerificationService"

echo "=== Running $N trials of '$FAULT' ==="

for i in $(seq 1 $N); do
  echo "--- $FAULT trial $i/$N ---"

docker exec -i "$EVENTS_CONTAINER" psql -U postgres -d events -q \
  -c "DELETE FROM event_store.verification_checkpoint;" 2>/dev/null || true

  FAULT_MODE=inject FAULT_TYPE=$FAULT dotnet run --project $INJECTOR
  dotnet run --project $VERIFIER
  FAULT_MODE=revert dotnet run --project $INJECTOR

  ACTIVE=$(docker exec -i "$EVENTS_CONTAINER" psql -U postgres -d events -tAc \
    "SELECT COUNT(*) FROM evaluation.injected_faults WHERE reverted = FALSE;")
  if [ "$ACTIVE" != "0" ]; then
    echo "ERROR: $ACTIVE active fault(s) after revert — halting."
    exit 1
  fi
done

echo "=== Done: $N trials of '$FAULT' ==="