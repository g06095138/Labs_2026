from pathlib import Path
import json
import platform
import numpy as np
import pandas as pd
import matplotlib
matplotlib.use("Agg")
import sklearn
from scipy.stats import randint
from sklearn.compose import ColumnTransformer
from sklearn.impute import SimpleImputer
from sklearn.metrics import (accuracy_score, precision_score, recall_score,
                             f1_score, roc_auc_score, confusion_matrix,
                             classification_report)
from sklearn.model_selection import (train_test_split, StratifiedKFold,
                                     RepeatedStratifiedKFold, GridSearchCV,
                                     RandomizedSearchCV)
from sklearn.neighbors import KNeighborsClassifier
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import OneHotEncoder, StandardScaler

data = pd.read_csv(Path(__file__).resolve().parent / "titanic.csv")
def display(value):
    print(value)
feature_cols = ["pclass", "sex", "age", "sibsp", "parch", "fare", "embarked"]
target_col = "survived"
numeric_features = ["pclass", "age", "sibsp", "parch", "fare"]
categorical_features = ["sex", "embarked"]
X = data[feature_cols].copy()
y = data[target_col].copy()
X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.20, random_state=42, stratify=y)
numeric_pipeline = Pipeline([("imputer", SimpleImputer(strategy="median")), ("scaler", StandardScaler())])
categorical_pipeline = Pipeline([("imputer", SimpleImputer(strategy="most_frequent")), ("onehot", OneHotEncoder(handle_unknown="ignore"))])
preprocessor = ColumnTransformer([("num", numeric_pipeline, numeric_features), ("cat", categorical_pipeline, categorical_features)])
def make_model():
    return Pipeline([("preprocessing", preprocessor), ("knn", KNeighborsClassifier())])

def evaluate_model(model):
    pred = model.predict(X_test)
    prob = model.predict_proba(X_test)[:, 1]
    return {"Accuracy": accuracy_score(y_test, pred), "Precision": precision_score(y_test, pred, zero_division=0),
            "Recall": recall_score(y_test, pred, zero_division=0), "F1": f1_score(y_test, pred, zero_division=0),
            "ROC-AUC": roc_auc_score(y_test, prob)}

print("Python:", platform.python_version(), "| scikit-learn:", sklearn.__version__)
print("Размер данных:", data.shape, "| train:", X_train.shape, "| test:", X_test.shape)
baseline = make_model().set_params(knn__n_neighbors=5, knn__weights="uniform", knn__metric="euclidean")
baseline.fit(X_train, y_train)
baseline_metrics = evaluate_model(baseline)
grid_params = {"knn__n_neighbors": [3, 5, 7, 9, 11, 15, 21, 31], "knn__weights": ["uniform", "distance"], "knn__metric": ["euclidean", "manhattan"]}
grid_search = GridSearchCV(make_model(), grid_params, scoring="f1", cv=StratifiedKFold(n_splits=5, shuffle=True, random_state=42), n_jobs=1, refit=True).fit(X_train, y_train)
random_params = {"knn__n_neighbors": randint(1, 41), "knn__weights": ["uniform", "distance"], "knn__metric": ["minkowski"], "knn__p": randint(1, 3)}
random_search = RandomizedSearchCV(make_model(), random_params, n_iter=30, scoring="f1", cv=RepeatedStratifiedKFold(n_splits=5, n_repeats=2, random_state=42), n_jobs=1, random_state=42, refit=True).fit(X_train, y_train)
searches = {"GridSearchCV": grid_search, "RandomizedSearchCV": random_search}
best_name = max(searches, key=lambda n: searches[n].best_score_)
best_search = searches[best_name]
best_metrics = evaluate_model(best_search.best_estimator_)
print("Baseline:", baseline_metrics)
print("GridSearchCV:", grid_search.best_score_, grid_search.best_params_)
print("RandomizedSearchCV:", random_search.best_score_, random_search.best_params_)
print("Best on CV:", best_name, best_search.best_params_)
print("Best test metrics:", best_metrics)
print("Confusion matrix:\n", confusion_matrix(y_test, best_search.best_estimator_.predict(X_test)))
