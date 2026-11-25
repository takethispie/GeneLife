#!/usr/bin/env node
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import ts from "typescript";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const dtoPath = join(root, "..", "..", "Genelife.Contracts", "Dtos", "FamilyStatus.cs");
const fixturesPath = join(root, "e2e", "admin", "family-fixtures.ts");

const dtoProperties = parseDtoProperties(readFileSync(dtoPath, "utf8"));

const schema = {
  FamilyStatus: {
    traits: "TraitDistributionStatus",
    households: ["HouseholdCompositionStatus"],
    projection: "ProjectionStalenessStatus",
    performance: "FamilyPerformanceStatus",
    health: ["FamilyHealthCheckStatus"]
  },
  TraitDistributionStatus: {
    categories: ["TraitCategoryStatus"]
  },
  TraitCategoryStatus: {
    values: ["TraitValueStatus"]
  },
  FamilyGenosPage: {
    genoses: ["FamilyGenosStatus"]
  },
  FamilyGenosStatus: {
    traits: ["TraitValueStatus"]
  },
  FamilyGenosDetailStatus: {
    genos: "FamilyGenosStatus",
    relationships: ["FamilyRelationshipStatus"],
    pregnancies: ["PregnancyStatusDto"],
    children: ["ChildStatusDto"],
    guardianships: ["GuardianshipStatusDto"],
    household: "HouseholdCompositionStatus"
  },
  FamilyRelationshipPage: {
    relationships: ["FamilyRelationshipStatus"]
  },
  AttractionExplanationStatus: {
    components: ["AttractionComponentStatus"]
  },
  PregnancyPageStatus: {
    pregnancies: ["PregnancyStatusDto"]
  },
  ChildPageStatus: {
    children: ["ChildStatusDto"]
  }
};

const fixtures = await loadFixtures(fixturesPath);
const failures = [];

const checks = [
  [fixtures.familyOverview, "FamilyStatus", "familyOverview"],
  [fixtures.familyGenos, "FamilyGenosStatus", "familyGenos"],
  [fixtures.familyGenosesPage, "FamilyGenosPage", "familyGenosesPage"],
  [fixtures.familyGenosDetail, "FamilyGenosDetailStatus", "familyGenosDetail"],
  [fixtures.familyRelationshipsPage, "FamilyRelationshipPage", "familyRelationshipsPage"],
  [fixtures.familyAttraction, "AttractionExplanationStatus", "familyAttraction"],
  [fixtures.familyPregnanciesPage, "PregnancyPageStatus", "familyPregnanciesPage"],
  [fixtures.familyChildrenPage, "ChildPageStatus", "familyChildrenPage"],
  [fixtures.familyGuardianships, "GuardianshipStatusDto", "familyGuardianships"]
];

for (const [value, typeName, path] of checks) {
  compareFixture(value, typeName, path);
}

if (failures.length > 0) {
  console.error("Admin family fixtures do not match the C# DTOs:");
  for (const failure of failures) console.error(`  - ${failure}`);
  console.error("Update clients/browser/e2e/admin/family-fixtures.ts to match FamilyStatus.cs.");
  process.exitCode = 1;
} else {
  console.info(`Admin family fixtures match ${dtoProperties.size} C# DTO shapes.`);
}

function parseDtoProperties(source) {
  const classes = new Map();
  const classPattern = /public sealed class (\w+)\s*\{/g;
  let match;
  while ((match = classPattern.exec(source)) !== null) {
    const name = match[1];
    const start = match.index + match[0].length;
    const next = source.indexOf("public sealed class ", start);
    const body = source.slice(start, next === -1 ? source.length : next);
    const properties = [
      ...body.matchAll(/public\s+[^\n{}]+?\s+(\w+)\s*\{\s*get;\s*set;\s*\}/g)
    ].map((property) => camelCase(property[1]));
    classes.set(name, properties);
  }
  return classes;
}

function camelCase(name) {
  return name.charAt(0).toLowerCase() + name.slice(1);
}

async function loadFixtures(filePath) {
  const source = readFileSync(filePath, "utf8");
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.ESNext,
      target: ts.ScriptTarget.ES2022
    },
    fileName: "family-fixtures.ts"
  });
  const tempDir = mkdtempSync(join(tmpdir(), "genelife-admin-fixtures-"));
  try {
    const tempFile = join(tempDir, "family-fixtures.mjs");
    writeFileSync(tempFile, outputText);
    return await import(pathToFileURL(tempFile).href);
  } finally {
    rmSync(tempDir, { recursive: true, force: true });
  }
}

function compareFixture(value, typeName, path) {
  if (Array.isArray(value)) {
    value.forEach((item, index) => compareFixture(item, typeName, `${path}[${index}]`));
    return;
  }
  if (value === null || typeof value !== "object") {
    failures.push(`${path}: expected an object for ${typeName}`);
    return;
  }

  const expected = dtoProperties.get(typeName);
  if (expected === undefined) {
    failures.push(`${path}: unknown C# DTO ${typeName}`);
    return;
  }

  const actual = Object.keys(value);
  const missing = expected.filter((key) => !actual.includes(key));
  const extra = actual.filter((key) => !expected.includes(key));
  if (missing.length > 0 || extra.length > 0) {
    failures.push(
      `${path} (${typeName}): missing [${missing.join(", ")}], extra [${extra.join(", ")}]`
    );
  }

  for (const [key, nestedType] of Object.entries(schema[typeName] ?? {})) {
    if (!(key in value)) continue;
    const nestedValue = value[key];
    if (Array.isArray(nestedType)) {
      compareFixture(nestedValue, nestedType[0], `${path}.${key}`);
    } else {
      compareFixture(nestedValue, nestedType, `${path}.${key}`);
    }
  }
}
