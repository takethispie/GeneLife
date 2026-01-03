import React from 'react';
import { useAppSelector, useAppDispatch } from '../store/hooks';
import { setData, setLoading } from '../store/slices/appSlice';
import api from '../services/api';
import './LandingPage.css';

const LandingPage: React.FC = () => {
  const dispatch = useAppDispatch();
  const { loading, error, data } = useAppSelector((state) => state.app);

  const handleTestApi = async (): Promise<void> => {
    try {
      dispatch(setLoading(true));
      // Example API call - replace with actual endpoint
      const response = await api.get('/test');
      dispatch(setData(response.data));
    } catch (error) {
      console.error('API call failed:', error);
    } finally {
      dispatch(setLoading(false));
    }
  };

  return (
    <div className="landing-page">
      <header className="landing-header">
        <h1>Welcome to GeneLife</h1>
        <p>Your genetic life simulation platform</p>
      </header>
      
      <main className="landing-main">
        <section className="hero-section">
          <h2>Explore Life Through Genetics</h2>
          <p>
            Discover the fascinating world of genetic simulation where life unfolds
            through complex interactions and evolutionary processes.
          </p>
          
          <div className="action-buttons">
            <button 
              className="primary-button"
              onClick={handleTestApi}
              disabled={loading}
            >
              {loading ? 'Loading...' : 'Test API Connection'}
            </button>
          </div>
          
          {data && (
            <div className="api-result">
              <h3>API Response:</h3>
              <pre>{JSON.stringify(data, null, 2)}</pre>
            </div>
          )}
          
          {error && (
            <div className="error-message">
              <p>Error: {error}</p>
            </div>
          )}
        </section>
        
        <section className="features-section">
          <h2>Features</h2>
          <div className="features-grid">
            <div className="feature-card">
              <h3>Genetic Simulation</h3>
              <p>Advanced algorithms simulate genetic inheritance and evolution</p>
            </div>
            <div className="feature-card">
              <h3>Life Cycles</h3>
              <p>Watch organisms grow, reproduce, and evolve over generations</p>
            </div>
            <div className="feature-card">
              <h3>Real-time Analytics</h3>
              <p>Monitor population dynamics and genetic diversity in real-time</p>
            </div>
          </div>
        </section>
      </main>
      
      <footer className="landing-footer">
        <p>&copy; 2024 GeneLife. All rights reserved.</p>
      </footer>
    </div>
  );
};

export default LandingPage;