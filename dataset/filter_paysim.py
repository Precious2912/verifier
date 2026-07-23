import pandas as pd

INPUT = "paysim_dataset.csv" #original paysim data (6m records)
OUTPUT = "paysim_c2c_transfers.csv" # filtered result

keep = ["step", "amount", "nameOrig", "nameDest"] #columns needed
chunks = []
for chunk in pd.read_csv(INPUT, chunksize=100_000):
    filtered = chunk[
        (chunk["type"] == "TRANSFER")
        & (chunk["nameOrig"].str.startswith("C"))
        & (chunk["nameDest"].str.startswith("C"))
    ]
    chunks.append(filtered[keep])

result = pd.concat(chunks, ignore_index=True)
result.to_csv(OUTPUT, index=False)

print(f"Filtered {len(result)} customer-to-customer transfers.")
print(f"Columns: {result.columns.tolist()}")
print(result.head())