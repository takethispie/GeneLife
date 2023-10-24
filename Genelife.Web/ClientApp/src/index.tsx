
import * as React from "react";
import * as ReactDOM from "react-dom";
import { store } from "./store/store";
import registerServiceWorker from "./registerServiceWorker";
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import Home from "./components/Home";
import { Provider } from "react-redux";

const router = createBrowserRouter([
  {
    path: "/",
    element: <Home/>,
  },
]);

ReactDOM.render(
        <React.StrictMode>
            <Provider store={store}>
                <RouterProvider router={router} />
            </Provider>
        </React.StrictMode>,
    document.getElementById("root"));

registerServiceWorker();
