import { Navigate, Route, Routes } from "react-router-dom"
import { HomePage } from '../components/pages/home/HomePage';
import { Navbar } from '../components/common/navbar/NavBar';
import { route } from "./routes";
import { ProfilePage } from "../components/pages/profile/ProfilePage";
import { LoginPage } from "../components/pages/login/LoginPage";
import { FullPageSpinner } from "../components/common/loader/FullPageSpinner";
import { useDispatch, useSelector } from "react-redux";
import { PublicRoute } from "./PublicRoute";
import { Alert, Snackbar } from "@mui/material";
import { type } from "../utils/types";
import { hideSnack } from "../actions/ui";
import { useEffect } from "react";

export const AppRouter = () => {
    console.log('Ambiente', process.env.REACT_APP_API_BASE_URL);

    const { ui: { loading, snackbar }, auth } = useSelector(state => state);
    const { isLogued } = auth;
    const dispatch = useDispatch();

    const handleCloseSnack = () => {
        dispatch(hideSnack());
    }

    useEffect(() => {
        if (isLogued) {
            localStorage.setItem('VETUSER', JSON.stringify(auth));
            return;
        }

        localStorage.removeItem('VETUSER');
    }, [isLogued])


    return (
        <div>
            <Snackbar open={snackbar?.open} autoHideDuration={3000} anchorOrigin={{ vertical: 'top', horizontal: 'center' }} onClose={handleCloseSnack}>
                <Alert onClose={handleCloseSnack} severity={snackbar?.severity}>
                    {snackbar?.message}
                </Alert>
            </Snackbar>
            {
                loading && (
                    <FullPageSpinner />
                )
            }
            <Routes>
                <Route
                    path={route.home}
                    element={
                        <PublicRoute
                            component={HomePage} />
                    } />

                <Route
                    path={route.login}
                    element={
                        <PublicRoute
                            restricted
                            navBar={false}
                            isLogued={isLogued}
                            component={LoginPage} />
                    } />

                <Route path="*" element={<Navigate to={route.home} />} />
            </Routes>
        </div>
    )
}
